CREATE TABLE sessions (
    id SERIAL PRIMARY KEY,
    title VARCHAR(150) NOT NULL,
    seats INT NOT NULL
);
CREATE TABLE tickets (
    id SERIAL PRIMARY KEY,
    session_id INT REFERENCES sessions(id),
    seat INT NOT NULL
);
INSERT INTO sessions (title, seats) VALUES ('Голабая лагуна', 50), ('Монстыр на каникулах', 30);
